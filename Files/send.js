let ch = 0;
let send = document.querySelector('#send');
let ta = document.querySelector('#text');
let subject = document.querySelector('#subject');
let to = document.querySelector('#to');
let error = document.querySelector("#error");
let e1 = document.querySelector('#e1');
let e2 = document.querySelector('#e2');
let e3 = document.querySelector('#e3');
let e4 = document.querySelector('#e4');
let sidebar = document.querySelector('.sidebar');
let full = document.querySelector('.full');
let save = document.querySelector('#save');
window.onresize = Resize;
ta.onclick = Refocus;
Resize();
document.addEventListener('keydown', e => {
    if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        Save();
    }
});

function Resize() {
    let fullComp = window.getComputedStyle(full);
    let erComp = window.getComputedStyle(error);
    let e1Comp = window.getComputedStyle(e1);
    let e2Comp = window.getComputedStyle(e2);
    let e3Comp = window.getComputedStyle(e3);
    let e4Comp = window.getComputedStyle(e4);
    let newHeightFloat = window.visualViewport.height - (parseFloat(e2Comp['marginTop']) * 3) - parseFloat(e1Comp['height']) - parseFloat(e2Comp['height']) - parseFloat(e4Comp['height']) - parseFloat(e3Comp['marginTop']) - parseFloat(fullComp['paddingTop']) - parseFloat(fullComp['paddingBottom']);
    if (erComp['display'] !== "none")
        newHeightFloat = newHeightFloat - parseFloat(e2Comp['marginTop']) - parseFloat(erComp['height']);
    e3.style.flex = '1';
    e3.style.height = newHeightFloat + 'px';
    Refocus();
}

function Refocus() {
    let nh = ta.clientHeight;
    if (ch > nh && document.activeElement === ta) {
        ta.blur();
        ta.focus();
    }
    ch = nh;
}

async function GoToAttachments() {
    if (await Save()) {
        window.location.assign("[PATH_PREFIX]/send/attachments?mailbox=" + GetQuery("mailbox"));
    }
}

async function GoToPreview() {
    if (await Save()) {
        window.location.assign("[PATH_PREFIX]/send/preview?mailbox=" + GetQuery("mailbox"));
    }
}

async function GoToContacts() {
    if (await Save()) {
        window.location.assign("[PATH_PREFIX]/send/contacts?mailbox=" + GetQuery("mailbox"));
    }
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    try {
        let url = "[PATH_PREFIX]/save-draft?mailbox=" + GetQuery("mailbox");
        if (subject != null)
            url = url + "&to=" + encodeURIComponent(to.value) + "&subject=" + encodeURIComponent(subject.value)
        let response = await fetch(url, { method: "POST", body: ta.value });
        if (response.status === 200) {
            let text = await response.text();
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
    try {
        let response = await fetch("/api[PATH_PREFIX]/delete-draft?mailbox=" + GetQuery("mailbox"));
        if (response.status === 200) {
            window.location.assign("[PATH_HOME]?mailbox=" + GetQuery("mailbox"));
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
}

function MessageChanged() {
    save.innerText = "Save";
    save.className = "green";
}

async function Send() {
    send.innerText = "Sending...";
    if (await Save()) {
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
