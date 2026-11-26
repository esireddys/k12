import { useEffect, useState } from 'react'
import { useItemImage } from '../api/useItemImage'

/** The printed item: the source of truth every edit is checked against. */
export function ItemPicture({ path }: { path?: string }) {
  const image = useItemImage(path)
  const [url, setUrl] = useState<string>()

  useEffect(() => {
    if (!image.data) return
    const next = URL.createObjectURL(image.data)
    setUrl(next)
    return () => URL.revokeObjectURL(next)
  }, [image.data])

  const frame = {
    border: '2px solid var(--border)',
    borderRadius: 14,
    background: '#fff',
    padding: 8,
    minHeight: 160,
    display: 'grid',
    placeItems: 'center',
  } as const

  if (!path) return <div style={frame}>This question has no picture.</div>
  if (image.isError)
    return (
      <div style={frame}>
        Picture {path} was not found. Run the pipeline import so data/out has it, and check Content:ImageRoot.
      </div>
    )
  return (
    <div style={frame}>
      {url ? <img src={url} alt="The printed item" style={{ maxWidth: '100%', height: 'auto' }} /> : 'Loading picture…'}
    </div>
  )
}
