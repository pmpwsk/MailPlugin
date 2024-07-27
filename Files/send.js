let ch = 0;
let send = document.getElementById("send");
let ta = document.getElementById("text");
let subject = document.getElementById("subject");
let to = document.getElementById("to");
let error = document.getElementById("error");
let e1 = document.getElementById("e1");
let e2 = document.getElementById("e2");
let e3 = document.getElementById("e3");
let e4 = document.getElementById("e4");
let sidebar = document.querySelector(".sidebar");
let full = document.querySelector(".full");
let save = document.getElementById("save");
let discard = document.getElementById("discard");
window.onresize = Resize;
ta.onclick = Refocus;
Resize();
document.addEventListener("keydown", e => {
    if (e.ctrlKey && e.key === "s") {
        e.preventDefault();
        Save();
    }
});

function Resize() {
    var fullComp = window.getComputedStyle(full);
    var erComp = window.getComputedStyle(error);
    var e1Comp = window.getComputedStyle(e1);
    var e2Comp = window.getComputedStyle(e2);
    var e3Comp = window.getComputedStyle(e3);
    var e4Comp = window.getComputedStyle(e4);
    var newHeightFloat = window.visualViewport.height - (parseFloat(e2Comp["marginTop"]) * 3) - parseFloat(e1Comp["height"]) - parseFloat(e2Comp["height"]) - parseFloat(e4Comp["height"]) - parseFloat(e3Comp["marginTop"]) - parseFloat(fullComp["paddingTop"]) - parseFloat(fullComp["paddingBottom"]);
    if (erComp["display"] !== "none")
        newHeightFloat = newHeightFloat - parseFloat(e2Comp["marginTop"]) - parseFloat(erComp["height"]);
    if (newHeightFloat < 300)
        newHeightFloat = 300;
    e3.style.flex = "1";
    e3.style.height = newHeightFloat + "px";
    Refocus();
}

function Refocus() {
    var nh = ta.clientHeight;
    if (ch > nh && document.activeElement === ta) {
        ta.blur();
        ta.focus();
    }
    ch = nh;
}

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
