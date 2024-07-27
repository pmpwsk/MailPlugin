async function Move(n) {
    if (await SendRequest(`move/do?mailbox=${GetQuery("mailbox")}&folder=${encodeURIComponent(GetQuery("folder"))}&message=${GetQuery("message")}&new=${n}`, "POST", true) === 200)
        window.location.assign(`.?mailbox=${GetQuery("mailbox")}&folder=${n}`);
    else ShowError("Connection failed.");
}