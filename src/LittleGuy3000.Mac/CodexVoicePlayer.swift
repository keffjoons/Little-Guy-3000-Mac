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
protocol LiveVoicePlaying: VoicePlaying {
    func prepareConversation(in host: NSView, syntheticInput: Bool)
    func setMuted(_ muted: Bool)
    func setOutputEnabled(_ enabled: Bool)
    func playTestInput(_ data: Data)
}

@MainActor
final class CodexVoicePlayer: NSObject, LiveVoicePlaying, WKNavigationDelegate, WKUIDelegate {
    var event: (([String: Any]) -> Void)?
    private var web: WKWebView?
    private var bridge: VoiceScriptBridge?
    private var conversation = false

    func prepare(in host: NSView) {
        prepare(in: host, conversation: false)
    }

    func prepareConversation(in host: NSView, syntheticInput: Bool = false) {
        prepare(in: host, conversation: true, syntheticInput: syntheticInput)
    }

    private func prepare(in host: NSView, conversation: Bool, syntheticInput: Bool = false) {
        stop()
        self.conversation = conversation
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .nonPersistent()
        config.mediaTypesRequiringUserActionForPlayback = []
        let bridge = VoiceScriptBridge(); bridge.owner = self; self.bridge = bridge
        config.userContentController.add(bridge, name: "voice")
        let view = WKWebView(frame: NSRect(x: 0, y: 0, width: 1, height: 1), configuration: config)
        view.navigationDelegate = self
        view.uiDelegate = self
        // Attach to the visible app window so WebKit does not suspend audio timers.
        host.addSubview(view, positioned: .below, relativeTo: nil)
        web = view
        view.loadHTMLString(Self.page(conversation: conversation, syntheticInput: syntheticInput), baseURL: URL(string: "https://localhost"))
    }

    func setMuted(_ muted: Bool) { web?.evaluateJavaScript("setMuted(\(muted));") }
    func setOutputEnabled(_ enabled: Bool) { web?.evaluateJavaScript("setOutputEnabled(\(enabled));") }

    // Synthetic audio is used only by the explicit live acceptance test.
    func playTestInput(_ data: Data) {
        web?.evaluateJavaScript("playTestInput('\(data.base64EncodedString())');")
    }

    func webView(_ webView: WKWebView, requestMediaCapturePermissionFor origin: WKSecurityOrigin,
                 initiatedByFrame frame: WKFrameInfo, type: WKMediaCaptureType,
                 decisionHandler: @escaping (WKPermissionDecision) -> Void) {
        decisionHandler(webView === web && conversation && frame.isMainFrame && origin.host == "localhost"
                        && origin.protocol == "https" && type == .microphone ? .grant : .deny)
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

    static let html = page(conversation: false)

    static func page(conversation: Bool, syntheticInput: Bool = false) -> String {
        let input = conversation && !syntheticInput ? """
        microphone = await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true,autoGainControl:true},video:false});
        if (closed) { microphone.getTracks().forEach(track => track.stop()); return; }
        microphone.getTracks().forEach(track => { track.enabled = false; pc.addTrack(track,microphone); });
        send({microphone:true});
        """ : """
        oscillator = ac.createOscillator(); const gain = ac.createGain(); gain.gain.value = 0;
        destination = ac.createMediaStreamDestination();
        oscillator.connect(gain).connect(destination); oscillator.start();
        pc.addTrack(destination.stream.getAudioTracks()[0], destination.stream);
        """
        return """
    <!doctype html><html><body><audio id="audio" autoplay></audio><script>
    const send = message => window.webkit.messageHandlers.voice.postMessage(message);
    let pc, ac, oscillator, timer, analyser, source, microphone, destination;
    let finishedAt = 0, lastSound = 0, heard = false, closed = false;
    const audio = document.getElementById('audio');
    let outputEnabled = \(!conversation);
    audio.muted = !outputEnabled;
    function setOutputEnabled(enabled) {
      outputEnabled = enabled; audio.muted = !enabled;
      if (!enabled) { heard = false; lastSound = 0; }
    }
    function closeVoice() {
      if (closed) return; closed = true;
      clearInterval(timer); audio.pause(); audio.srcObject = null;
      if (microphone) microphone.getTracks().forEach(track => track.stop());
      if (pc) pc.close(); if (oscillator) oscillator.stop(); if (ac) void ac.close();
    }
    window.addEventListener('pagehide', closeVoice);
    function setMuted(muted) { if (microphone) microphone.getAudioTracks().forEach(track => track.enabled = !muted); }
    async function playTestInput(base64) {
      const bytes = Uint8Array.from(atob(base64),c=>c.charCodeAt(0));
      const buffer = await ac.decodeAudioData(bytes.buffer);
      const input = ac.createBufferSource(); input.buffer = buffer;
      input.connect(destination); input.start();
    }
    (async () => {
      pc = new RTCPeerConnection(); ac = new AudioContext();
      await ac.resume();
      \(input)
      const channel = pc.createDataChannel('oai-events');
      channel.onopen = () => send({ready:true});
      channel.onmessage = event => {
        try {
          const message = JSON.parse(event.data);
          if (message.type === 'error') send({error:'Codex voice reported an error.'});
          if (message.type === 'input_transcript.added') send({input:true});
        } catch {}
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
        if (outputEnabled && analyser) {
          analyser.getFloatTimeDomainData(samples);
          let peak = 0; for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
          if (peak > 0.002) {
            lastSound = performance.now();
            if (!heard) { heard = true; send({audible:true}); }
          }
        }
        if (\(conversation) && heard && performance.now() - lastSound > 750) { heard = false; send({quiet:true}); }
        if (finishedAt && heard && performance.now() - finishedAt > 1000 && performance.now() - lastSound > 750) {
          finishedAt = 0; send({finished:true});
        }
      }, 100);
    })().catch(() => send({error:'The Codex voice player could not start.'}));
    </script></body></html>
    """
    }
}

@MainActor
private final class VoiceScriptBridge: NSObject, WKScriptMessageHandler {
    weak var owner: CodexVoicePlayer?
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard message.frameInfo.isMainFrame, let body = message.body as? [String: Any] else { return }
        owner?.event?(body)
    }
}
