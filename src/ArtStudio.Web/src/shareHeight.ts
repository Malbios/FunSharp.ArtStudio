/** A ref that keeps the element's height in a CSS variable on the page, for sticky elements to stack below it. */
export const shareHeight = (variable: string) => (element: HTMLElement | null) => {
  if (!element) return
  const root = document.documentElement.style
  const observer = new ResizeObserver(() => root.setProperty(variable, `${element.getBoundingClientRect().height}px`))
  observer.observe(element)
  return () => {
    observer.disconnect()
    root.removeProperty(variable)
  }
}
