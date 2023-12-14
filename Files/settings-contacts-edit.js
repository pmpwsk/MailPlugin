let save = document.querySelector("#save");

function Changed() {
    save.innerText = "Save";
    save.className = "green";
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    try {
        let name = document.querySelector("#name");
        let favorite = document.querySelector("#favorite");
        if (name.value === "")
            ShowError("Enter a name!");
        else {
            let response = await fetch("/api[PATH_PREFIX]/contacts/set?mailbox=" + GetQuery("mailbox") + "&email=" + encodeURIComponent(GetQuery("email")) + "&name=" + encodeURIComponent(name.value) + "&favorite=" + favorite.checked);
            switch (response.status) {
                case 200:
                    save.innerText = "Saved!";
                    save.className = "";
                    window.location.assign("[PATH_PREFIX]/settings/contacts?mailbox=" + GetQuery("mailbox"));
                    return;
                default:
                    ShowError("Connection failed.");
                    break;
            }
        }
    } catch {
        ShowError("Connection failed.");
    }
    save.innerText = "Save";
    save.className = "green";
    return;
}

async function Delete() {
    try {
        let response = await fetch("/api[PATH_PREFIX]/contacts/delete?mailbox=" + GetQuery("mailbox") + "&email=" + encodeURIComponent(GetQuery("email")));
        if (response.status === 200)
            window.location.assign("[PATH_PREFIX]/settings/contacts?mailbox=" + GetQuery("mailbox"));
        else ShowError("Connection failed.");
    } catch {
        ShowError("Connection failed.");
    }
}