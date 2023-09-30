async function Create() {
    let address = document.querySelector("#address");
    if (address.value === "") {
        ShowError("Enter an address.");
    } else {
        let response = await fetch("/api[PATH_PREFIX]/create-mailbox?address=" + encodeURIComponent(address.value));
        if (response.status === 200) {
            let text = await response.text();
            if (text == "format") {
                ShowError("Invalid format.");
            } else if (text == "exists") {
                ShowError("Another mailbox with this address already exists.");
            } else if (text.startsWith("[PATH_PREFIX]/manage?mailbox=")) {
                window.location.assign(text);
            } else {
                ShowError("Connection failed.");
            }
        } else {
            ShowError("Connection failed.");
        }
    }
}