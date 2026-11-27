import { describe, expect, it } from 'vitest'
import { selectionNotice, uploadFailure } from './photoMessages'

describe('selectionNotice', () => {
  it('ne dit rien quand toutes les photos choisies ont une place', () => {
    expect(selectionNotice(2, 2, 5)).toBeNull()
    expect(selectionNotice(1, 5, 5)).toBeNull()
  })

  it('annonce les photos laissées de côté, au singulier comme au pluriel', () => {
    expect(selectionNotice(2, 1, 5)).toBe('Tu as choisi 2 photos : seule la première est ajoutée (5 au maximum par annonce).')
    expect(selectionNotice(4, 2, 5)).toBe('Tu as choisi 4 photos : seules les 2 premières sont ajoutées (5 au maximum par annonce).')
  })
})

describe('uploadFailure', () => {
  it('nomme le fichier en cause', () => {
    expect(uploadFailure('faux.jpg', 'Le fichier n’est pas une image valide.', 0)).toBe('« faux.jpg » : Le fichier n’est pas une image valide.')
  })

  it('dit combien de photos n’ont pas été envoyées ensuite', () => {
    expect(uploadFailure('a.jpg', 'Refusé.', 1)).toBe('« a.jpg » : Refusé. La photo suivante n’a pas été envoyée.')
    expect(uploadFailure('a.jpg', 'Refusé.', 3)).toBe('« a.jpg » : Refusé. Les 3 photos suivantes n’ont pas été envoyées.')
  })
})
