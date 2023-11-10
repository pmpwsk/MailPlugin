async function Delete() {
    let deleteText = document.querySelector("#deleteButton");
    if (deleteText.textContent === "Delete") {
        deleteText.textContent = "Delete?";
    } else {
        let response = await fetch("/api[PATH_PREFIX]/delete-message?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message"));
        if (response.status === 200) {
            let text = await response.text();
            if (text == "ok") {
                window.location.assign("[PATH_HOME]?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder"));
            } else {
                ShowError("Connection failed.");
            }
        } else {
            ShowError("Connection failed.");
        }
    }
}

async function Unread() {
    let response = await fetch("/api[PATH_PREFIX]/unread?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message"))
    if (response.status === 200) {
        window.location.assign("[PATH_HOME]?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder"));
    } else {
        ShowError("Connection failed.");
    }
}

async function Reply() {
    let response = await fetch("/api[PATH_PREFIX]/reply?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message"));
    if (response.status === 200) {
        window.location.assign("[PATH_PREFIX]/send?mailbox=" + GetQuery("mailbox"));
    } else {
        ShowError("Connection failed.");
    }
}

async function FindOriginal(encodedMessageId) {
    let b = document.querySelector("#find");
    b.textContent = "finding...";
    let response = await fetch("/api[PATH_PREFIX]/find?mailbox=" + GetQuery("mailbox") + "&id=" + encodedMessageId);
    b.textContent = "find";
    if (response.status === 200) {
        let text = await response.text();
        if (text.startsWith("mailbox=")) {
            window.location.assign("[PATH_HOME]?" + text);
            return;
        }
        else if (text === "no") {
            ShowError("The original message was deleted or the sender did something wrong.");
            return;
        }
    }
    ShowError("Connection failed.");
}