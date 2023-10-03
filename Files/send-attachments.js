async function Upload() {
    let selector = document.getElementById("update-file").files;
    if (files.length === 0) {
        ShowError("Select a file first.");
        return;
    }
    let file = files[0];
    let form = new FormData();
    form.append("file", file);
    let request = new XMLHttpRequest();
    request.open("POST", "[PATH_PREFIX]/upload-attachment?mailbox=" + GetQuery("mailbox"));
    request.upload.addEventListener("progress", event => {
        document.querySelector("#uploadButton").firstElementChild.innerText = ((event.loaded / event.total) * 100).toFixed(2) + "%";
    });
    request.onreadystatechange = () => {
        if (request.readyState == 4) {
            switch (request.status) {
                case 200:
                    window.location.reload();
                    break;
                default:
                    ShowError("Connection failed.")
                    break;
            }
        }
    };
    request.send(form);
}