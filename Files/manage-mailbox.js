async function Add() {
    let username = document.querySelector("#username");
    if (username.value === "") {
        ShowError("Enter a username.");
    } else {
        let response = await fetch("/api[PATH_PREFIX]/add-access?mailbox=" + GetQuery("mailbox") + "&username=" + encodeURIComponent(username.value));
        if (response.status === 200) {
            let text = await response.text();
            if (text == "invalid") {
                ShowError("Invalid username.");
            } else if (text == "exists") {
                ShowError("This user already has access.");
            } else if (text == "ok") {
                window.location.reload();
            } else {
                ShowError("Connection failed.");
            }
        } else {
            ShowError("Connection failed.");
        }
    }
}

async function Remove(id) {
    let response = await fetch("/api[PATH_PREFIX]/remove-access?mailbox=" + GetQuery("mailbox") + "&id=" + encodeURIComponent(id));
    if (response.status === 200) {
        let text = await response.text();
        if (text == "ok") {
            window.location.reload();
        } else {
            ShowError("Connection failed.");
        }
    } else {
        ShowError("Connection failed.");
    }
}

async function Delete() {
    let deleteText = document.querySelector("#deleteButton").firstElementChild;
    if (deleteText.textContent === "Delete mailbox") {
        deleteText.textContent = "Delete everything?";
    } else {
        let response = await fetch("/api[PATH_PREFIX]/delete-mailbox?mailbox=" + GetQuery("mailbox"));
        if (response.status === 200) {
            let text = await response.text();
            if (text == "ok") {
                window.location.assign("[PATH_PREFIX]/manage");
            } else {
                ShowError("Connection failed.");
            }
        } else {
            ShowError("Connection failed.");
        }
    }
}

function GetQuery(q) {
    try {
        let query = new URLSearchParams(window.location.search);
        if (query.has(q)) {
            return query.get(q);
        } else {
            return "";
        }
    } catch {
        return "";
    }
}