let save = document.getElementById("save");
let del = document.getElementById("del");

function Changed() {
    save.innerText = "Save";
    save.className = "green";
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    var name = document.getElementById("name");
    var favorite = document.getElementById("favorite");
    if (name.value === "")
        ShowError("Enter a name!");
    else if (await SendRequest(`contacts/set?mailbox=${GetQuery("mailbox")}&email=${encodeURIComponent(GetQuery("email"))}&name=${encodeURIComponent(name.value)}&favorite=${favorite.checked}`, "POST", true) === 200) {
        save.innerText = "Saved!";
        save.className = "";
        window.location.assign(`contacts?mailbox=${GetQuery("mailbox")}`);
        return;
    } else ShowError("Connection failed.");
    save.innerText = "Save";
    save.className = "green";
}

async function Delete() {
    if (del.innerText == "Delete")
        del.innerText = "Delete?";
    else if (await SendRequest(`contacts/delete?mailbox=${GetQuery("mailbox")}&email=${encodeURIComponent(GetQuery("email"))}`, "POST", true) === 200)
        window.location.assign(`contacts?mailbox=${GetQuery("mailbox")}`);
    else ShowError("Connection failed.");
}