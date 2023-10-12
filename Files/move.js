async function Move(n) {
    let response = await fetch("/api[PATH_PREFIX]/move?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message") + "&new=" + n)
    if (response.status === 200) {
        window.location.assign("[PATH_HOME]?mailbox=" + GetQuery("mailbox") + "&folder=" + n);
    } else {
        ShowError("Connection failed.");
    }
}