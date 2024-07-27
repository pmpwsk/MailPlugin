async function Delete() {
    var deleteText = document.getElementById("deleteButton");
    if (deleteText.textContent === "Delete")
        deleteText.textContent = "Delete?";
    else if (await SendRequest(`delete-message?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}`, "POST", true) === 200)
        window.location.assign(`.?mailbox=${GetQuery("mailbox")}&folder=${GetQuery("folder")}`);
    else ShowError("Connection failed.");
}

async function Unread() {
    if (await SendRequest(`unread?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}`, "POST", true) === 200)
        window.location.assign(`.?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}`);
    else ShowError("Connection failed.");
}

async function Reply() {
    if (await SendRequest(`reply?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}`, "POST", true) === 200)
        window.location.assign(`send?mailbox=${GetQuery("mailbox")}`);
    else ShowError("Connection failed.");
}

async function FindOriginal(encodedMessageId) {
    var b = document.getElementById("find");
    b.textContent = "finding...";
    var response = await fetch(`find?mailbox=${GetQuery("mailbox")}&id=${encodedMessageId}`, {method: "POST"});
    b.textContent = "find";
    if (response.status === 200) {
        var text = await response.text();
        if (text.startsWith("mailbox=")) {
            window.location.assign(`.?${text}`);
            return;
        }
        else if (text === "no") {
            ShowError("The original message was deleted or the sender did something wrong.");
            return;
        }
    }
    ShowError("Connection failed.");
}