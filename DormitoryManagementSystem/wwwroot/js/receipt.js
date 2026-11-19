const printReceipt = document.getElementById("print-receipt");
if (printReceipt) {
    printReceipt.hidden = false;
    printReceipt.addEventListener("click", () => window.print());
}
