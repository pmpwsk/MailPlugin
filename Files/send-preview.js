let send = document.querySelector('#send');

async function Send() {
    send.innerText = "Sending...";
    try {
        let response = await fetch("/api[PATH_PREFIX]/send-draft?mailbox=" + GetQuery("mailbox"));
        if (response.status === 200) {
            let text = await response.text();
            if (text.startsWith("message=")) {
                window.location.assign("[PATH_HOME]?mailbox=" + GetQuery("mailbox") + "&folder=Sent&" + text);
                return;
            } else {
                switch (text) {
                    case "invalid-to":
                        ShowError("Enter at least one recipient.");
                        break;
                    case "invalid-subject":
                        ShowError("Enter a subject.");
                        break;
                    case "invalid-text":
                        ShowError("Enter a message.");
                        break;
                    default:
                        ShowError("Connection failed.");
                        break;
                }
            }
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
    send.innerText = "Send";
}