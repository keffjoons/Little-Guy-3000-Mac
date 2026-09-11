import Foundation

@main
struct ProtocolTests {
    @MainActor static func main() async throws {
        var stream = AnswerStream(threadID: "thread-a")
        stream.turnID = "turn-a"
        precondition(stream.accept("item/agentMessage/delta", ["threadId": "other", "delta": "wrong"]) == nil)
        precondition(stream.accept("item/agentMessage/delta", ["threadId": "thread-a", "turnId": "old", "delta": "wrong"]) == nil)
        precondition(stream.accept("item/agentMessage/delta", ["threadId": "thread-a", "turnId": "turn-a", "itemId": "a", "delta": "Hello "]) == "delta")
        _ = stream.accept("item/agentMessage/delta", ["threadId": "thread-a", "turnId": "turn-a", "itemId": "a", "delta": "🌱"])
        precondition(stream.text == "Hello 🌱")
        _ = stream.accept("item/agentMessage/delta", ["threadId": "thread-a", "turnId": "turn-a", "itemId": "b", "delta": "Final"])
        precondition(stream.text == "Final")
        precondition(stream.accept("turn/completed", ["threadId": "thread-a", "turn": ["id": "old", "status": "completed"]]) == nil)
        precondition(stream.accept("turn/completed", ["threadId": "thread-a", "turn": ["id": "turn-a", "status": "completed"]]) == "completed")
        precondition(stream.accept("item/agentMessage/delta", ["threadId": "thread-a", "itemId": "b", "delta": String(repeating: "x", count: 200_001)]) == "oversized")
        _ = stream.accept("turn/completed", ["threadId": "thread-a", "turn": ["id": "turn-a", "status": "failed", "error": ["codexErrorInfo": "unauthorized"]]])
        precondition(stream.failureMessage?.contains("sign-in expired") == true)
        _ = stream.accept("turn/completed", ["threadId": "thread-a", "turn": ["id": "turn-a", "status": "failed", "error": ["codexErrorInfo": "usageLimitExceeded"]]])
        precondition(stream.failureMessage?.contains("usage limit") == true)
        print("PASS: Unicode streaming, item boundaries, stale thread/turn rejection, response limit")

        let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let home = root.appendingPathComponent(".local/mac-protocol-test-\(UUID().uuidString)")
        let executable = root.appendingPathComponent("tests/Mac/fake-codex.py").path
        let client = CodexConnection()
        try await client.start(executable: executable, home: home, configuration: "# synthetic test\n")
        let account = try await client.request("account/read")
        precondition(account["isolated"] as? Bool == true)
        let approval = try await client.request("test/approval")
        precondition(approval["declined"] as? Bool == true)
        let tool = try await client.request("test/tool")
        precondition(tool["rejected"] as? Bool == true)
        client.toolCall = { params in
            precondition(params["threadId"] as? String == "live" && params["tool"] as? String == "inspect_window")
            return ["success": true, "contentItems": [["type": "inputText", "text": "synthetic window"]]]
        }
        let action = try await client.request("test/action")
        precondition(action["success"] as? Bool == true)
        let stillDenied = try await client.request("test/approval")
        precondition(stillDenied["declined"] as? Bool == true)
        let noAccess = try await client.request("test/app-access")
        precondition(noAccess["action"] as? String == "decline")
        client.appAccessRequest = { request in
            precondition(request["threadId"] as? String == "live")
            return ["action": "accept", "content": [:], "_meta": ["persist": "session"]]
        }
        let appAccess = try await client.request("test/app-access")
        precondition(appAccess["action"] as? String == "accept")
        client.appAccessRequest = nil
        client.toolCall = nil
        do { _ = try await client.request("test/error"); fatalError("Expected protocol failure") }
        catch { precondition(error.localizedDescription.contains("-42")) }
        let delayed = Task { try await client.request("test/hang") }
        try await Task.sleep(for: .milliseconds(50))
        client.stop()
        do { _ = try await delayed.value; fatalError("Pending request must fail on stop") } catch {}
        try await client.start(executable: executable, home: home, configuration: "# synthetic test\n")
        _ = try await client.request("account/read")
        client.stop()
        print("PASS: isolated child environment, fragmented JSONL, approval/tool denial, protocol errors, stop/reconnect")
    }
}
