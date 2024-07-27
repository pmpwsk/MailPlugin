async function AddContact(email) {
    if (await SendRequest(`contacts/add?mailbox=${GetQuery("mailbox")}&email=${email}`, "POST", true) === 200)
        window.location.assign("../send?mailbox=" + GetQuery("mailbox"));
    else ShowError("Connection failed.");
}

function Search() {
    var searchValue = document.getElementById("search").value;
    var searchQuery = GetQuery("search");
    if (searchQuery === "null")
        searchQuery = "";
    if (searchValue === searchQuery)
        return;
    if (searchValue === "")
        window.location.assign("../send/contacts?mailbox=" + GetQuery("mailbox"));
    else window.location.assign("../send/contacts?mailbox=" + GetQuery("mailbox") + "&search=" + searchValue);
}