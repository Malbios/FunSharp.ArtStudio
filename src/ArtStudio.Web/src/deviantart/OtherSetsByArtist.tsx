export function OtherSetsByArtist({ count }: { count: number }) {
  const label = count === 0 ? 'no other sets' : count === 1 ? '1 other set' : `${count} other sets`
  return <span title="Other sets inspired by this DeviantArt user, in any stage">· {label}</span>
}
