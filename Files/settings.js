let nameInput = document.getElementById("name-input");
let nameButton = document.getElementById("save-name");
let footerInput = document.getElementById("footer-input");
let footerButton = document.getElementById("save-footer");
let externalImagesInput = document.getElementById("external-images");
let externalImagesButton = document.getElementById("save-external-images");

function NameChanged() {
    nameButton.innerText = "Save";
    nameButton.className = "green";
}

async function SaveName() {
    nameButton.innerText = "Saving...";
    nameButton.className = "green";
    if (await SendRequest(`settings/set-name?mailbox=${GetQuery("mailbox")}&name=${encodeURIComponent(nameInput.value)}`, "POST", true) === 200) {
        nameButton.innerText = "Saved!";
        nameButton.className = "";
    } else {
        ShowError("Connection failed.");
        nameButton.innerText = "Save";
        nameButton.className = "green";
    }
}

function FooterChanged() {
    footerButton.innerText = "Save";
    footerButton.className = "green";
}

async function SaveFooter() {
    footerButton.innerText = "Saving...";
    footerButton.className = "green";
    if (await SendRequest(`settings/set-footer?mailbox=${GetQuery("mailbox")}&footer=${encodeURIComponent(footerInput.value)}`, "POST", true) === 200) {
        footerButton.innerText = "Saved!";
        footerButton.className = "";
    } else {
        ShowError("Connection failed.");
        footerButton.innerText = "Save";
        footerButton.className = "green";
    }
}

async function SaveExternalImages() {
    externalImagesButton.innerText = "Saving...";
    externalImagesButton.className = "green";
    if (await SendRequest(`settings/set-external-images?mailbox=${GetQuery("mailbox")}&value=${externalImagesInput.checked}`, "POST", true) === 200) {
        externalImagesButton.innerText = "Saved!";
        externalImagesButton.className = "";
    } else {
        ShowError("Connection failed.");
        externalImagesButton.innerText = "Save";
        externalImagesButton.className = "green";
    }
}