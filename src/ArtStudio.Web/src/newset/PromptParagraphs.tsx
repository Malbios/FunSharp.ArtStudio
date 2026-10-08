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
}

export function PromptParagraphs({ paragraphs, onChange }: Props) {
  const labels = paragraphLabels(paragraphs.length)
  const characterCount = paragraphs.filter((_, index) => isCharacter(index, paragraphs.length)).length

  function changeParagraph(index: number, text: string) {
    onChange(paragraphs.map((paragraph, position) => (position === index ? text : paragraph)))
  }

  function paste(index: number, event: ClipboardEvent<HTMLTextAreaElement>) {
    if (imageFileFrom(event.clipboardData)) return
    const text = event.clipboardData.getData('text')
    if (!text) return
    event.preventDefault()
    const textarea = event.currentTarget
    const cleaned = cleanPrompt(text)
    if (!hasSeveralParagraphs(cleaned)) {
      insertAtSelection(textarea, cleaned)
      return
    }
    const before = textarea.value.slice(0, textarea.selectionStart)
    const after = textarea.value.slice(textarea.selectionEnd)
    onChange(pasteIntoParagraphs(paragraphs, index, before, toParagraphs(cleaned), after))
  }

  return (
    <div className="prompt-paragraphs">
      {paragraphs.map((paragraph, index) => (
        <div key={index} className="prompt-paragraph">
          <div className="prompt-paragraph-label">
            <label htmlFor={`prompt-paragraph-${index}`}>{labels[index]}</label>
            {characterCount > 1 && isCharacter(index, paragraphs.length) && (
              <button type="button" className="link-button" onClick={() => onChange(removeParagraph(paragraphs, index))}>
                Remove
              </button>
            )}
          </div>
          <textarea
            id={`prompt-paragraph-${index}`}
            value={paragraph}
            rows={3}
            onChange={(e) => changeParagraph(index, e.target.value)}
            onPaste={(e) => paste(index, e)}
          />
          {index === characterCount - 1 && (
            <button type="button" className="link-button add-character" onClick={() => onChange(addCharacter(paragraphs))}>
              + Add character
            </button>
          )}
        </div>
      ))}
    </div>
  )
}
