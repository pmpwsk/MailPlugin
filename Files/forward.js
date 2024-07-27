async function Quote() {
    if (await SendRequest(`forward/quote?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}&everything=${document.getElementById("everything").checked}`, "POST", true) === 200)
        window.location.assign(`send?mailbox=${GetQuery("mailbox")}`);
    else ShowError("Connection failed.");
}

async function Original() {
    var send = document.getElementById('send');
    send.innerText = "Sending...";
    var response = await SendRequest(`forward/original?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}&info=${document.getElementById("info").checked}&to=${encodeURIComponent(document.getElementById("to").value)}`, "POST");
    if (response === "invalid-to")
        ShowError("Enter at least one recipient.");
    else if (typeof response === "string" && response.startsWith("message=")) {
        window.location.assign(`.?mailbox=${GetQuery("mailbox")}&folder=Sent&${response}`);
        return;
    } else  ShowError("Connection failed.");
    send.innerText = "Send";
}