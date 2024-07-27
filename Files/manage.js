async function Create() {
    var address = document.getElementById("address");
    if (address.value === "")
        ShowError("Enter an address.");
    else {
        var response = await SendRequest(`manage/create-mailbox?address=${encodeURIComponent(address.value)}`, "POST");
        switch (response) {
            case "format":
                ShowError("Invalid format.");
                break;
            case "exists":
                ShowError("Another mailbox with this address already exists.");
                break;
            default:
                if (typeof response === "string" && response.startsWith("mailbox="))
                    window.location.assign(`manage?${response}`);
                else ShowError("Connection failed.");
                break;
        }
    }
}