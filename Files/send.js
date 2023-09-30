let ch = 0;
let ta = document.querySelector('#text');
let error = document.querySelector("#error");
let e1 = document.querySelector('#e1');
let e2 = document.querySelector('#e2');
let e3 = document.querySelector('#e3');
let e4 = document.querySelector('#e4');
let sidebar = document.querySelector('.sidebar');
let full = document.querySelector('.full');
let save = document.querySelector('#save');
let back = document.querySelector('#back');
window.onresize = Resize;
ta.onclick = Refocus;
Resize();
Load();

function Resize() {
    let fullComp = window.getComputedStyle(full);
    let erComp = window.getComputedStyle(error);
    let e1Comp = window.getComputedStyle(e1);
    let e2Comp = window.getComputedStyle(e2);
    let e3Comp = window.getComputedStyle(e3);
    let e4Comp = window.getComputedStyle(e4);
    let newHeightFloat = window.visualViewport.height - (parseFloat(e2Comp['marginTop']) * 3) - parseFloat(e1Comp['height']) - parseFloat(e2Comp['height']) - parseFloat(e4Comp['height']) - parseFloat(e3Comp['marginTop']) - parseFloat(fullComp['paddingTop']) - parseFloat(fullComp['paddingBottom']);
    if (erComp['display'] !== "none")
        newHeightFloat = newHeightFloat - parseFloat(e2Comp['marginTop']) - parseFloat(erComp['height']);
    let newHeight = Math.floor(newHeightFloat);
    e3.style.flex = '1';
    e3.style.height = newHeightFloat + 'px';
    Refocus();
}

function Refocus() {
    let nh = ta.clientHeight;
    if (ch > nh && document.activeElement === ta) {
        ta.blur();
        ta.focus();
    }
    ch = nh;
}

async function GoToAttachments() {
    await Save();
    window.location.assign(window.location.pathname + "/attachments?mailbox=" + GetQuery("mailbox"));
}

async function Save() {
    ShowError("Not implemented.");
}

async function Discard() {
    ShowError("Not implemented.");
}

function MessageChanged() {
    ShowError("Not implemented.");
}

async function Send() {
    ShowError("Not implemented.");
}

function ShowError(message) {
    error.firstElementChild.innerText = message;
    error.style.display = "block";
    Resize();
}

function HideError() {
    error.style.display = "none";
    Resize();
}
