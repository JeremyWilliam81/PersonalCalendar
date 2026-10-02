// Phone vs wide layouts depend on `matchMedia('(max-width: 599px)')` (research V6).
// jsdom has no matchMedia, so tests install this stub and flip it as needed.

type Listener = (event: MediaQueryListEvent) => void

let narrow = false
const listeners = new Set<Listener>()

function matches(query: string): boolean {
  return query.includes('max-width: 599px') ? narrow : false
}

export function setNarrowViewport(next: boolean): void {
  narrow = next
  for (const listener of [...listeners]) listener({ matches: next } as MediaQueryListEvent)
}

export function installMatchMediaStub(): void {
  narrow = false
  listeners.clear()
  window.matchMedia = (query: string) =>
    ({
      get matches() {
        return matches(query)
      },
      media: query,
      onchange: null,
      addEventListener: (_type: string, listener: Listener) => listeners.add(listener),
      removeEventListener: (_type: string, listener: Listener) => listeners.delete(listener),
      addListener: (listener: Listener) => listeners.add(listener),
      removeListener: (listener: Listener) => listeners.delete(listener),
      dispatchEvent: () => false,
    }) as unknown as MediaQueryList
}
