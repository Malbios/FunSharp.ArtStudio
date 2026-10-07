function deviantArtUserUrl(username: string): string {
  return `https://www.deviantart.com/${encodeURIComponent(username)}`
}

export function DeviantArtUserLink({ username }: { username: string }) {
  return (
    <a href={deviantArtUserUrl(username)} target="_blank" rel="noreferrer" onClick={(event) => event.stopPropagation()}>
      {username}
    </a>
  )
}
