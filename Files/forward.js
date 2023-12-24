async function Quote() {
    let response = await fetch("/api[PATH_PREFIX]/forward/quote?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message") + "&everything=" + document.querySelector("#everything").checked);
    if (response.status === 200) {
        window.location.assign("[PATH_PREFIX]/send?mailbox=" + GetQuery("mailbox"));
    } else {
        ShowError("Connection failed.");
    }
}

async function Original() {
    let send = document.querySelector('#send');
    send.innerText = "Sending...";
    try {
        let response = await fetch("/api[PATH_PREFIX]/forward/original?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message") + "&info=" + document.querySelector("#info").checked + "&to=" + encodeURIComponent(document.querySelector("#to").value));
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