import Foundation

@MainActor
final class SessionFixture: CodexTransport {
    var notification: ((String, [String: Any]) -> Void)?
    var disconnected: (() -> Void)?
    var signedIn = true
    var stopCount = 0
    var finishInsideStart = false
    var turns: [[String: Any]] = []
    func start(executable: String, home: URL, configuration: String) async throws {}
    func stop() { stopCount += 1 }
    func request(_ method: String, _ params: [String: Any]) async throws -> [String: Any] {
        switch method {
        case "account/read": return ["account": signedIn ? ["planType": "pro"] as Any : NSNull()]
        case "model/list": return ["data": [["id": "model-a", "displayName": "Model A", "isDefault": true], ["id": "model-b", "displayName": "Model B"]]]
        case "thread/start": return ["thread": ["id": "thread-test"]]
        case "turn/start":
            turns.append(params)
            if finishInsideStart { complete("A fast answer") }
            return ["turn": ["id": "turn-test"]]
        case "account/login/start": return ["authUrl": "https://auth.openai.com/test-sign-in"]
        default: return [:]
        }
    }
    func complete(_ text: String) {
        notification?("item/completed", ["threadId": "thread-test", "turnId": "turn-test", "item": ["type": "agentMessage", "text": text]])
        notification?("turn/completed", ["threadId": "thread-test", "turn": ["id": "turn-test", "status": "completed"]])
    }
    func fail() {
        notification?("turn/completed", ["threadId": "thread-test", "turn": ["id": "turn-test", "status": "failed", "error": ["codexErrorInfo": "usageLimitExceeded"]]])
    }
}

@main struct SessionTests {
    @MainActor static func main() async throws {
        let suite = "LittleGuySessionTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let backend = SessionFixture()
        let session = CompanionSession(transport: backend, defaults: defaults)
        let connect = { await session.connect(executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "") }
        backend.signedIn = false
        await connect()
        session.draft = "Hello"
        precondition(session.connection == .signedOut && !session.canSend)
        await session.send()
        precondition(backend.turns.isEmpty, "A signed-out user must not send a question")
        let loginURL = await session.signIn()
        precondition(loginURL != nil)
        precondition(session.connection == .signingIn && !session.canSend)
        backend.signedIn = true
        await connect()
        precondition(session.canSend)

        session.imageData = Data([1, 2, 3]); session.imageName = "example.png"
        session.selectMode(.explain)
        precondition(session.draft == "Hello" && session.imageData != nil)
        session.selectModel("model-b")
        precondition(session.draft == "Hello" && session.imageData != nil)
        await session.send()
        precondition(session.busy && session.draft.isEmpty && session.imageData == nil)
        session.draft = "A new draft while waiting"
        backend.fail()
        precondition(!session.busy && session.draft == "A new draft while waiting")
        precondition(session.imageData == Data([1, 2, 3]) && session.imageName == "example.png")
        precondition(session.error?.contains("usage limit") == true)

        session.newConversation(); session.selectMode(.ask)
        session.draft = "Stop this question"; session.imageData = Data([4, 5])
        await session.send(); await session.cancel()
        precondition(session.connection == .ready && !session.busy)
        precondition(session.draft == "Stop this question" && session.imageData == Data([4, 5]))
        precondition(backend.stopCount > 0 && session.modelID == "model-b")
        backend.complete("Stale answer after cancel")
        precondition(session.messages.last?.text != "Stale answer after cancel")

        session.newConversation(); session.draft = "Answer immediately"
        backend.finishInsideStart = true
        await session.send()
        precondition(!session.busy && session.draft.isEmpty)
        precondition(session.messages.last?.text == "A fast answer")
        session.draft = "Follow-up without a screenshot"
        await session.send()
        let lastInput = backend.turns.last?["input"] as? [[String: Any]] ?? []
        precondition(!lastInput.contains(where: { $0["type"] as? String == "image" }))

        backend.finishInsideStart = false
        session.draft = "Interrupted request that must not become history"
        await session.send(); await session.cancel(); await session.send()
        let restoredInput = backend.turns.last?["input"] as? [[String: Any]] ?? []
        let restoredText = restoredInput.first?["text"] as? String ?? ""
        precondition(restoredText.contains("Assistant: A fast answer"), "Reconnect must preserve completed conversation context")
        precondition(!restoredText.contains("No answer received."), "Interrupted replies must not be restored as completed answers")
        precondition(restoredText.components(separatedBy: "Interrupted request that must not become history").count == 2)
        precondition(!restoredInput.contains(where: { $0["type"] as? String == "image" }))
        session.shutdown()
        print("PASS: sign-in gate; draft/model preservation; image retry; cancel/reconnect; stale events; fast completion; fresh-image boundaries")
    }
}
