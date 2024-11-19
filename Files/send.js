let send = document.getElementById("send");
let ta = document.getElementById("text");
let subject = document.getElementById("subject");
let to = document.getElementById("to");
let error = document.getElementById("error");
let save = document.getElementById("save");
let discard = document.getElementById("discard");
document.addEventListener("keydown", e => {
    if (e.ctrlKey && e.key === "s") {
        e.preventDefault();
        Save();
    }
});

async function GoToAttachments() {
    if (await Save())
        window.location.assign(`send/attachments?mailbox=${GetQuery("mailbox")}`);
}

async function GoToPreview() {
    if (await Save())
        window.location.assign(`send/preview?mailbox=${GetQuery("mailbox")}`);
}

async function GoToContacts() {
    if (await Save())
        window.location.assign(`send/contacts?mailbox=${GetQuery("mailbox")}`);
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    try {
        var url = `send/save-draft?mailbox=${GetQuery("mailbox")}`;
        if (subject != null)
            url = url + "&to=" + encodeURIComponent(to.value) + "&subject=" + encodeURIComponent(subject.value)
        var response = await fetch(url, { method: "POST", body: ta.value });
        if (response.status === 200) {
            var text = await response.text();
            switch (text) {
                case "ok":
                    save.innerText = "Saved!";
                    save.className = "";
                    return true;
                    break;
                case "invalid-to":
                    ShowError("Invalid recipient(s).");
                    break;
                default:
                    ShowError("Connection failed.");
                    break;
            }
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
    save.innerText = "Save";
    save.className = "green";
    return false;
}

async function Discard() {
    if (discard.innerText === "Discard")
        discard.innerText = "Discard?";
    else if (await SendRequest(`send/delete-draft?mailbox=${GetQuery("mailbox")}`, "POST", true) === 200)
        window.location.assign(".?mailbox=" + GetQuery("mailbox"));
    else ShowError("Connection failed.");
}

function MessageChanged() {
    save.innerText = "Save";
    save.className = "green";
}

async function Send() {
    send.innerText = "Sending...";
    if (await Save()) {
        var response = await SendRequest(`send/send-draft?mailbox=${GetQuery("mailbox")}`, "POST");
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
    }
    send.innerText = "Send";
}

function ShowError(message) {
    error.firstElementChild.innerText = message;
    error.style.display = "block";
    Resize();
}

function HideError() {
    error.style.display = "none";
    Resize();
}
