async function Add() {
    try {
        let email = document.querySelector("#email");
        let name = document.querySelector("#name");
        let favorite = document.querySelector("#favorite");
        if (email.value === "")
            ShowError("Enter an email address!");
        else if (name.value === "")
            ShowError("Enter a name!");
        else {
            let response = await fetch("/api[PATH_PREFIX]/contacts/set?mailbox=" + GetQuery("mailbox") + "&email=" + encodeURIComponent(email.value) + "&name=" + encodeURIComponent(name.value) + "&favorite=" + favorite.checked);
            switch (response.status) {
                case 200:
                    email.value = "";
                    name.value = "";
                    favorite.checked = false;
                    window.location.reload();
                    break;
                case 418:
                    ShowError("Invalid email address!");
                    break;
                default:
                    ShowError("Connection failed.");
                    break;
            }
        }
    } catch {
        ShowError("Connection failed.");
    }
}