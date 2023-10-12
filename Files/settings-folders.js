async function Delete(f) {
    let deleteText = document.querySelector("#" + f);
    if (deleteText.textContent === "Delete") {
        deleteText.textContent = "Delete?";
    } else {
        let response = await fetch("/api[PATH_PREFIX]/delete-folder?mailbox=" + GetQuery("mailbox") + "&folder=" + f);
        if (response.status === 200) {
            window.location.reload();
        } else {
            ShowError("Connection failed.");
        }
    }
}

async function Create() {
    let name = document.querySelector("#name");
    if (name.value === "") {
        ShowError("Enter a name!");
    } else {
        let response = await fetch("/api[PATH_PREFIX]/create-folder?mailbox=" + GetQuery("mailbox") + "&name=" + encodeURIComponent(name.value));
        switch (response.status) {
            case 200:
                name.value = "";
                window.location.reload();
                break;
            case 409:
                ShowError("Another folder with this name already exists!");
                break;
            default:
                ShowError("Connection failed.");
                break;
        }
    }
}