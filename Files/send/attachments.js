async function Upload() {
    var files = document.getElementById("update-file").files;
    if (files.length === 0) {
        ShowError("Select a file!");
        return;
    }
    var file = files[0];
    if (file.size > 10485760) {
        ShowError("This file is too large! (limit: 10MB)");
        return;
    }
    var form = new FormData();
    form.append("file", file);
    var request = new XMLHttpRequest();
    request.open("POST", `attachments/upload?mailbox=${GetQuery("mailbox")}`);
    request.upload.addEventListener("progress", event => {
        document.getElementById("uploadButton").innerText = ((event.loaded / event.total) * 100).toFixed(2) + "%";
    });
    request.onreadystatechange = () => {
        if (request.readyState == 4) {
            document.getElementById("uploadButton").innerText = "Add";
            switch (request.status) {
                case 200:
                    window.location.reload();
                    break;
                case 413:
                    ShowError("This file is too large! (limit: 10MB)");
                    break;
                default:
                    ShowError("Connection failed.");
                    break;
            }
        }
    };
    request.send(form);
}

async function Delete(attachmentId) {
    if (await SendRequest(`attachments/delete?mailbox=${GetQuery("mailbox")}&attachment=${attachmentId}`, "POST", true))
        window.location.reload();
    else ShowError("Connection failed.");
}