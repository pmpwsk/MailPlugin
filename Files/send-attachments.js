async function Upload() {
    let files = document.getElementById("update-file").files;
    if (files.length === 0) {
        ShowError("Select a file!");
        return;
    }
    let file = files[0];
    if (file.size > 10485760) {
        ShowError("This file is too large! (limit: 10MB)");
        return;
    }
    let form = new FormData();
    form.append("file", file);
    let request = new XMLHttpRequest();
    request.open("POST", "[PATH_PREFIX]/upload-attachment?mailbox=" + GetQuery("mailbox"));
    request.upload.addEventListener("progress", event => {
        document.querySelector("#uploadButton").innerText = ((event.loaded / event.total) * 100).toFixed(2) + "%";
    });
    request.onreadystatechange = () => {
        if (request.readyState == 4) {
            document.querySelector("#uploadButton").innerText = "Add";
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
    try {
        let response = await fetch("/api[PATH_PREFIX]/delete-attachment?mailbox=" + GetQuery("mailbox") + "&attachment=" + attachmentId);
        if (response.status === 200) {
            window.location.reload();
        } else {
            ShowError("Connection failed.");
        }
    } catch {
        ShowError("Connection failed.");
    }
}