import { act, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LiveRegionProvider } from './LiveRegion'
import { useAnnounce } from './useAnnounce'

function Announcer({ message }: { message: string }) {
  const announce = useAnnounce()
  return <button onClick={() => announce(message)}>announce</button>
}

describe('LiveRegion', () => {
  it('renders announced messages in a polite live region', () => {
    render(
      <LiveRegionProvider>
        <Announcer message="Deleted Dentist." />
      </LiveRegionProvider>,
    )

    act(() => screen.getByRole('button').click())

    const region = screen.getByRole('status')
    expect(region).toHaveAttribute('aria-live', 'polite')
    expect(region).toHaveTextContent('Deleted Dentist.')
  })

  it('re-announces the same message', () => {
    render(
      <LiveRegionProvider>
        <Announcer message="Saved." />
      </LiveRegionProvider>,
    )

    act(() => screen.getByRole('button').click())
    const first = screen.getByRole('status').textContent
    act(() => screen.getByRole('button').click())

    expect(screen.getByRole('status').textContent).not.toBe(first)
    expect(screen.getByRole('status')).toHaveTextContent('Saved.')
  })
})
