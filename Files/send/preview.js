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
                window.location.assign(`..?mailbox=${GetQuery("mailbox")}&folder=Sent&${response}`);
                return;
            }
            ShowError("Connection failed.");
            break;
    }
    send.innerText = "Send";
}