async function AddContact(email) {
    try {
        let response = await fetch("/api[PATH_PREFIX]/draft/add-recipient?mailbox=" + GetQuery("mailbox") + "&email=" + email);
        if (response.status === 200) {
            window.location.assign("[PATH_PREFIX]/send?mailbox=" + GetQuery("mailbox"));
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
}

function Search() {
    let searchValue = document.querySelector("#search").value;
    let searchQuery = GetQuery("search");
    if (searchQuery === "null")
        searchQuery = "";
    if (searchValue === searchQuery)
        return;
    if (searchValue === "")
        window.location.assign("[PATH_PREFIX]/send/contacts?mailbox=" + GetQuery("mailbox"));
    else window.location.assign("[PATH_PREFIX]/send/contacts?mailbox=" + GetQuery("mailbox") + "&search=" + searchValue);
}