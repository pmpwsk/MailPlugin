async function Delete(f, id) {
    var deleteText = document.getElementById("" + id);
    if (deleteText.textContent === "Delete")
        deleteText.textContent = "Delete?";
    else if (await SendRequest(`folders/delete?mailbox=${GetQuery("mailbox")}&folder=${f}`, "POST", true) === 200)
        window.location.reload();
    else ShowError("Connection failed.");
}

async function Create() {
    var name = document.getElementById("name");
    if (name.value === "")
        ShowError("Enter a name!");
    else switch (await SendRequest(`folders/create?mailbox=${GetQuery("mailbox")}&name=${encodeURIComponent(name.value)}`, "POST", true)) {
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