import AppKit
import WebKit

@MainActor
protocol VoicePlaying: AnyObject {
    var event: (([String: Any]) -> Void)? { get set }
    func prepare(in host: NSView)
    func answer(_ sdp: String)
    func finishWhenQuiet()
    func stop()
}

@MainActor
final class CodexVoicePlayer: NSObject, VoicePlaying, WKNavigationDelegate {
    var event: (([String: Any]) -> Void)?
    private var web: WKWebView?
    private var bridge: VoiceScriptBridge?

    func prepare(in host: NSView) {
        stop()
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .nonPersistent()
        config.mediaTypesRequiringUserActionForPlayback = []
        let bridge = VoiceScriptBridge(); bridge.owner = self; self.bridge = bridge
        config.userContentController.add(bridge, name: "voice")
        let view = WKWebView(frame: NSRect(x: 0, y: 0, width: 1, height: 1), configuration: config)
        view.navigationDelegate = self
        // Attach to the visible app window so WebKit does not suspend audio timers.
        host.addSubview(view, positioned: .below, relativeTo: nil)
        web = view
        view.loadHTMLString(Self.html, baseURL: URL(string: "https://localhost"))
    }

    func answer(_ sdp: String) {
        guard let data = try? JSONSerialization.data(withJSONObject: ["type": "answer", "sdp": sdp]),
              let json = String(data: data, encoding: .utf8) else { return }
        web?.evaluateJavaScript("void pc.setRemoteDescription(\(json)).catch(() => send({error:'Voice negotiation failed.'}));")
    }

    func finishWhenQuiet() { web?.evaluateJavaScript("finishedAt = performance.now();") }

    func stop() {
        let view = web; web = nil
        bridge?.owner = nil; bridge = nil
        view?.configuration.userContentController.removeScriptMessageHandler(forName: "voice")
        view?.evaluateJavaScript("closeVoice();")
        view?.loadHTMLString("", baseURL: nil)
        view?.removeFromSuperview()
    }

    func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
        guard webView === web else { return }
        event?(["error": "The voice player stopped. Try again."])
    }

    // This page never asks for microphone access. A silent track clocks the duplex
    // voice transport; microphone recognition remains local in VoiceInput.
    static let html = """
    <!doctype html><html><body><audio id="audio" autoplay></audio><script>
    const send = message => window.webkit.messageHandlers.voice.postMessage(message);
    let pc, ac, oscillator, timer, analyser, source;
    let finishedAt = 0, lastSound = 0, heard = false;
    const audio = document.getElementById('audio');
    function closeVoice() {
      clearInterval(timer); audio.pause(); audio.srcObject = null;
      if (pc) pc.close(); if (oscillator) oscillator.stop(); if (ac) void ac.close();
    }
    (async () => {
      pc = new RTCPeerConnection(); ac = new AudioContext();
      oscillator = ac.createOscillator(); const gain = ac.createGain(); gain.gain.value = 0;
      const destination = ac.createMediaStreamDestination();
      oscillator.connect(gain).connect(destination); oscillator.start(); await ac.resume();
      pc.addTrack(destination.stream.getAudioTracks()[0], destination.stream);
      const channel = pc.createDataChannel('oai-events');
      channel.onopen = () => send({ready:true});
      channel.onmessage = event => {
        try { const message = JSON.parse(event.data); if (message.type === 'error') send({error:'Codex voice reported an error.'}); } catch {}
      };
      pc.onconnectionstatechange = () => {
        if (pc.connectionState === 'failed' || pc.connectionState === 'disconnected') send({error:'Codex voice disconnected. Try again.'});
      };
      pc.ontrack = event => {
        const stream = event.streams[0] || new MediaStream([event.track]);
        audio.srcObject = stream;
        source = ac.createMediaStreamSource(stream); analyser = ac.createAnalyser(); analyser.fftSize = 1024;
        source.connect(analyser);
        void audio.play().catch(() => send({error:'Audio playback was blocked. Try Preview voice again.'}));
      };
      await pc.setLocalDescription(await pc.createOffer());
      if (pc.iceGatheringState !== 'complete') await new Promise(resolve => {
        const changed = () => { if (pc.iceGatheringState === 'complete') { pc.removeEventListener('icegatheringstatechange', changed); resolve(); } };
        pc.addEventListener('icegatheringstatechange', changed); setTimeout(resolve, 2500);
      });
      send({sdp:pc.localDescription.sdp});
      const samples = new Float32Array(1024);
      timer = setInterval(() => {
        if (analyser) {
          analyser.getFloatTimeDomainData(samples);
          let peak = 0; for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
          if (peak > 0.002) {
            lastSound = performance.now();
            if (!heard) { heard = true; send({audible:true}); }
          }
        }
        if (finishedAt && heard && performance.now() - finishedAt > 1000 && performance.now() - lastSound > 750) {
          finishedAt = 0; send({finished:true});
        }
      }, 100);
    })().catch(() => send({error:'The Codex voice player could not start.'}));
    </script></body></html>
    """
}

@MainActor
private final class VoiceScriptBridge: NSObject, WKScriptMessageHandler {
    weak var owner: CodexVoicePlayer?
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard message.frameInfo.isMainFrame, let body = message.body as? [String: Any] else { return }
        owner?.event?(body)
    }
}
