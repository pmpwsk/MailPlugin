let nameInput = document.querySelector("#name-input");
let nameButton = document.querySelector("#save-name");
let footerInput = document.querySelector("#footer-input");
let footerButton = document.querySelector("#save-footer");

function NameChanged() {
    nameButton.innerText = "Save";
    nameButton.className = "green";
}

async function SaveName() {
    nameButton.innerText = "Saving...";
    nameButton.className = "green";
    try {
        let response = await fetch("/api[PATH_PREFIX]/set-name?mailbox=" + GetQuery("mailbox") + "&name=" + encodeURIComponent(nameInput.value));
        if (response.status === 200) {
            nameButton.innerText = "Saved!";
            nameButton.className = "";
            return;
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
    nameButton.innerText = "Save";
    nameButton.className = "green";
    return false;
}

function FooterChanged() {
    footerButton.innerText = "Save";
    footerButton.className = "green";
}

async function SaveFooter() {
    footerButton.innerText = "Saving...";
    footerButton.className = "green";
    try {
        let response = await fetch("/api[PATH_PREFIX]/set-footer?mailbox=" + GetQuery("mailbox") + "&footer=" + encodeURIComponent(footerInput.value));
        if (response.status === 200) {
            footerButton.innerText = "Saved!";
            footerButton.className = "";
            return;
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
    footerButton.innerText = "Save";
    footerButton.className = "green";
    return false;
}