async function Add() {
    var email = document.getElementById("email");
    var name = document.getElementById("name");
    var favorite = document.getElementById("favorite");
    if (email.value === "")
        ShowError("Enter an email address!");
    else if (name.value === "")
        ShowError("Enter a name!");
    else switch (await SendRequest(`contacts/set?mailbox=${GetQuery("mailbox")}&email=${encodeURIComponent(email.value)}&name=${encodeURIComponent(name.value)}&favorite=${favorite.checked}`, "POST", true)) {
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