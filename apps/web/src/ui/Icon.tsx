/**
 * Icono SVG de la librería aprobada (game-icons.net, CC BY 3.0).
 * Se usa en estados vacíos y herramientas; decorativo (alt="").
 */
export function Icon({ name, size = 28 }: { name: string; size?: number }) {
  return (
    <img
      src={`/icons/${name}.svg`}
      alt=""
      width={size}
      height={size}
      style={{ display: 'block', margin: '0 auto' }}
    />
  )
}
