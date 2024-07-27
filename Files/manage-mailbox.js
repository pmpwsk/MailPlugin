async function Add() {
    var username = document.getElementById("username");
    if (username.value === "")
        ShowError("Enter a username.");
    else switch (await SendRequest(`manage/add-access?mailbox=${GetQuery("mailbox")}&username=${encodeURIComponent(username.value)}`, "POST")) {
        case "invalid":
            ShowError("Invalid username.");
            break;
        case "ok":
            window.location.reload();
            break;
        default:
            ShowError("Connection failed.");
            break;
    }
}

async function Remove(id) {
    if (await SendRequest(`manage/remove-access?mailbox=${GetQuery("mailbox")}&id=${encodeURIComponent(id)}`, "POST", true) === 200)
        window.location.reload();
    else ShowError("Connection failed.");
}

async function Delete() {
    var deleteText = document.getElementById("deleteButton").firstElementChild;
    if (deleteText.textContent === "Delete mailbox")
        deleteText.textContent = "Delete everything?";
    else if (await SendRequest(`manage/delete-mailbox?mailbox=${GetQuery("mailbox")}`, "POST", true) === 200)
        window.location.assign("manage");
    else ShowError("Connection failed.");
}