window.readClipboardText = async function () {
    try {
        return await navigator.clipboard.readText()
    } catch (err) {
        console.error("Clipboard read failed:", err)
        return ""
    }
}
