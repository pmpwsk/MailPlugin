let save = document.getElementById("save");

function Changed() {
    save.innerText = "Save";
    save.className = "green";
}

async function Save() {
    save.innerText = "Saving...";
    save.className = "green";
    if (await SendRequest("auth/set?mailbox=" + GetQuery("mailbox")
        + "&connection-secure=" + document.getElementById("connection-secure").checked
        + "&connection-ptr=" + document.getElementById("connection-ptr").checked
        + "&spf-min=" + document.getElementById("spf-min").value
        + "&dkim-min=" + document.getElementById("dkim-min").value
        + "&dmarc-enough=" + document.getElementById("dmarc-enough").checked
        + "&dmarc-min=" + document.getElementById("dmarc-min").value, "POST", true) === 200) {
        save.innerText = "Saved!";
        save.className = "";
    } else {
        ShowError("Connection failed.");
        save.innerText = "Save";
        save.className = "green";
    }
}