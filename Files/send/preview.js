let send = document.getElementById('send');

async function Send() {
    send.innerText = "Sending...";
    var response = await SendRequest(`send-draft?mailbox=${GetQuery("mailbox")}`, "POST");
    switch (response) {
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
            if (typeof response === "string" && response.startsWith("message=")) {
                window.location.assign(`.?mailbox=${GetQuery("mailbox")}&folder=Sent&${response}`);
                return;
            }
            ShowError("Connection failed.");
            break;
    }
    send.innerText = "Send";
}

async function FindOriginal(encodedMessageId) {
    var b = document.getElementById("find");
    b.textContent = "finding...";
    var response = await fetch(`../find?mailbox=${GetQuery("mailbox")}&id=${encodedMessageId}`, {method: "POST"});
    b.textContent = "find";
    if (response.status === 200) {
        var text = await response.text();
        if (text.startsWith("mailbox=")) {
            window.location.assign(`../?${text}`);
            return;
        }
        else if (text === "no") {
            ShowError("The original message was deleted or the sender did something wrong.");
            return;
        }
    }
    ShowError("Connection failed.");
}