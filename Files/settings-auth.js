let save = document.querySelector("#save");

function Changed() {
    save.innerText = "Save";
    save.className = "green";
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    try {
        let response = await fetch("/api[PATH_PREFIX]/set-auth?mailbox=" + GetQuery("mailbox")
            + "&connection-secure=" + document.querySelector("#connection-secure").checked
            + "&connection-ptr=" + document.querySelector("#connection-ptr").checked
            + "&spf-min=" + document.querySelector("#spf-min").value
            + "&dkim-min=" + document.querySelector("#dkim-min").value
            + "&dmarc-enough=" + document.querySelector("#dmarc-enough").checked
            + "&dmarc-min=" + document.querySelector("#dmarc-min").value
        );
        if (response.status === 200) {
            save.innerText = "Saved!";
            save.className = "";
            return;
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
    save.innerText = "Save";
    save.className = "green";
    return false;
}