import type { ClipboardEvent } from 'react'
import { cleanPrompt } from '../prompt/cleanPrompt'
import { insertAtSelection } from '../prompt/insertAtSelection'
import {
  addCharacter,
  hasSeveralParagraphs,
  isCharacter,
  paragraphLabels,
  pasteIntoParagraphs,
  removeParagraph,
  toParagraphs,
} from '../prompt/paragraphs'
import { imageFileFrom } from './imageSource'

interface Props {
  paragraphs: string[]
  onChange: (paragraphs: string[]) => void
  onError: (message: string) => void
  /** Opens the modify dialog for a box; Modify buttons are hidden without it. */
  onModify?: (index: number) => void
  modifyDisabled?: boolean
}

type ClipboardAction = 'append' | 'replace'

const textareaId = (index: number) => `prompt-paragraph-${index}`

export function PromptParagraphs({ paragraphs, onChange, onError, onModify, modifyDisabled }: Props) {
  const labels = paragraphLabels(paragraphs.length)
  const characterCount = paragraphs.filter((_, index) => isCharacter(index, paragraphs.length)).length

  function changeParagraph(index: number, text: string) {
    onChange(paragraphs.map((paragraph, position) => (position === index ? text : paragraph)))
  }

  /** Puts cleaned text between `start` and `end` of the box; several paragraphs spread over the following boxes. */
  function insertCleaned(index: number, textarea: HTMLTextAreaElement, text: string, start: number, end: number) {
    const cleaned = cleanPrompt(text)
    if (!hasSeveralParagraphs(cleaned)) {
      textarea.setSelectionRange(start, end)
      insertAtSelection(textarea, cleaned)
      return
    }
    const before = textarea.value.slice(0, start)
    const after = textarea.value.slice(end)
    onChange(pasteIntoParagraphs(paragraphs, index, before, toParagraphs(cleaned), after))
  }

  function paste(index: number, event: ClipboardEvent<HTMLTextAreaElement>) {
    if (imageFileFrom(event.clipboardData)) return
    const text = event.clipboardData.getData('text')
    if (!text) return
    event.preventDefault()
    const textarea = event.currentTarget
    insertCleaned(index, textarea, text, textarea.selectionStart, textarea.selectionEnd)
  }

  async function fromClipboard(index: number, action: ClipboardAction) {
    let text: string
    try {
      text = await navigator.clipboard.readText()
    } catch {
      onError('The browser did not allow reading the clipboard.')
      return
    }
    if (!text.trim()) return

    const textarea = document.getElementById(textareaId(index)) as HTMLTextAreaElement
    const length = textarea.value.length
    if (action === 'replace') {
      insertCleaned(index, textarea, text, 0, length)
      return
    }
    insertCleaned(index, textarea, text, length, length)
  }

  const addCharacterButton = (
    <button type="button" className="link-button add-character" onClick={() => onChange(addCharacter(paragraphs))}>
      + Add character
    </button>
  )

  return (
    <div className="prompt-paragraphs">
      {characterCount === 0 && addCharacterButton}
      {paragraphs.map((paragraph, index) => (
        <div key={index} className="prompt-paragraph">
          <div className="prompt-paragraph-label">
            <label htmlFor={textareaId(index)}>{labels[index]}</label>
            <button type="button" className="link-button" onClick={() => void fromClipboard(index, 'append')}>
              Append from clipboard
            </button>
            <button type="button" className="link-button" onClick={() => void fromClipboard(index, 'replace')}>
              Replace with clipboard
            </button>
            {onModify && (
              <button
                type="button"
                className="link-button"
                disabled={modifyDisabled || paragraph.trim() === ''}
                onClick={() => onModify(index)}
              >
                Modify
              </button>
            )}
            {characterCount > 1 && isCharacter(index, paragraphs.length) && (
              <button type="button" className="link-button" onClick={() => onChange(removeParagraph(paragraphs, index))}>
                Remove
              </button>
            )}
          </div>
          <textarea
            id={textareaId(index)}
            value={paragraph}
            rows={3}
            onChange={(e) => changeParagraph(index, e.target.value)}
            onPaste={(e) => paste(index, e)}
          />
          {index === characterCount - 1 && addCharacterButton}
        </div>
      ))}
    </div>
  )
}
