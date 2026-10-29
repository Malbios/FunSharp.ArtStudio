import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { postUrlFrom } from './deviationLink.js'

const post = 'https://www.deviantart.com/someartist/art/Misty-Forest-123456'
const otherPost = 'https://www.deviantart.com/otherartist/art/Lake-654321'

describe('postUrlFrom', () => {
  it('prefers the link around a thumbnail over the page it is on', () => {
    assert.equal(postUrlFrom(post, otherPost), post)
    assert.equal(postUrlFrom(post, 'https://www.deviantart.com/someartist/gallery'), post)
  })

  it('uses the post page when the clicked image has no post link', () => {
    assert.equal(postUrlFrom(undefined, post), post)
    assert.equal(postUrlFrom('https://www.deviantart.com/someartist', post), post)
  })

  it('accepts the subdomain and deviation-id forms', () => {
    assert.equal(postUrlFrom('https://someartist.deviantart.com/art/Misty-Forest-123456', undefined), 'https://someartist.deviantart.com/art/Misty-Forest-123456')
    assert.equal(postUrlFrom('https://www.deviantart.com/deviation/123456', undefined), 'https://www.deviantart.com/deviation/123456')
  })

  it('refuses profiles, galleries, image files and other sites', () => {
    assert.equal(postUrlFrom('https://www.deviantart.com/someartist', 'https://www.deviantart.com/someartist/gallery'), null)
    assert.equal(postUrlFrom('https://images-wixmp-ed30a86b8c4ca887773594c2.wixmp.com/f/abc/def.jpg', undefined), null)
    assert.equal(postUrlFrom('https://example.com/someartist/art/Misty-Forest-123456', undefined), null)
    assert.equal(postUrlFrom('not a url', undefined), null)
  })
})
