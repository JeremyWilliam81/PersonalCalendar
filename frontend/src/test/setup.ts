import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach } from 'vitest'
import { setLocale } from '../lib/locale'
import { installMatchMediaStub } from './viewport'

// Formatting follows the device locale in the app; tests pin en-US.
setLocale('en-US')

// Wide screen unless a test calls setNarrowViewport(true).
installMatchMediaStub()
beforeEach(() => {
  installMatchMediaStub()
  window.history.replaceState(null, '', '/')
})

// jsdom has no <dialog> modal support.
if (!HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.setAttribute('open', '')
  }
  HTMLDialogElement.prototype.show = function (this: HTMLDialogElement) {
    this.setAttribute('open', '')
  }
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.removeAttribute('open')
    this.dispatchEvent(new Event('close'))
  }
}

afterEach(() => {
  cleanup()
})
