async function Quote() {
    let response = await fetch("/api[PATH_PREFIX]/forward/quote?mailbox=" + GetQuery("mailbox") + "&folder=" + GetQuery("folder") + "&message=" + GetQuery("message") + "&everything=" + document.querySelector("#everything").checked);
    if (response.status === 200) {
        window.location.assign("[PATH_PREFIX]/send?mailbox=" + GetQuery("mailbox"));
    } else {
        ShowError("Connection failed.");
    }
}