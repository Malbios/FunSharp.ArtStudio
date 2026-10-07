/**
 * Replaces the textarea's selection with text as if the user had typed it.
 * execCommand is deprecated but is the only way that keeps the browser's undo history (Ctrl+Z);
 * setting the value directly wipes it. It also fires the input event that React's onChange listens to.
 */
export function insertAtSelection(textarea: HTMLTextAreaElement, text: string): void {
  textarea.focus()
  if (document.execCommand('insertText', false, text)) return

  textarea.setRangeText(text, textarea.selectionStart, textarea.selectionEnd, 'end')
  textarea.dispatchEvent(new Event('input', { bubbles: true }))
}
