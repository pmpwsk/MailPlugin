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