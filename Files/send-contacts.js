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