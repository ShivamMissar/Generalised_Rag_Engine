import { describe, expect, it } from 'vitest'
import { parseReport, toSegments } from './report'

describe('toSegments', () => {
  it('splits text and citations', () => {
    expect(toSegments('Built APIs [1][3], and more.')).toEqual([
      { kind: 'text', text: 'Built APIs ' },
      { kind: 'cite', n: 1 },
      { kind: 'cite', n: 3 },
      { kind: 'text', text: ', and more.' },
    ])
  })

  it('returns plain text when there are no citations', () => {
    expect(toSegments('No evidence.')).toEqual([{ kind: 'text', text: 'No evidence.' }])
  })
})

describe('parseReport', () => {
  const report = `Overall match: Moderate. Strong .NET skills but no Kubernetes.

Requirements:
- C# and .NET: Met. Three years of experience [2].
- Experience: cloud platforms: Partly met. Azure only [1].
- Kubernetes: Not met. There is no mention of Kubernetes.

Gaps: The candidate lacks Kubernetes experience.

What to highlight:
- Cutting API response time by 40% [3].`

  it('recognises every part of the expected format', () => {
    expect(parseReport(report).map((line) => line.kind)).toEqual([
      'overall', 'heading', 'requirement', 'requirement', 'requirement', 'heading', 'heading', 'bullet',
    ])
  })

  it('reads the overall rating', () => {
    expect(parseReport(report)[0]).toEqual({
      kind: 'overall',
      rating: 'Moderate',
      segments: [{ kind: 'text', text: 'Strong .NET skills but no Kubernetes.' }],
    })
  })

  it('reads each requirement and its status, even when the requirement contains a colon', () => {
    const requirements = parseReport(report).filter((line) => line.kind === 'requirement')
    expect(requirements.map((line) => [line.requirement, line.status])).toEqual([
      ['C# and .NET', 'Met'],
      ['Experience: cloud platforms', 'Partly met'],
      ['Kubernetes', 'Not met'],
    ])
  })

  it('keeps the text after a heading', () => {
    expect(parseReport(report)[5]).toEqual({
      kind: 'heading',
      title: 'Gaps',
      segments: [{ kind: 'text', text: 'The candidate lacks Kubernetes experience.' }],
    })
  })

  it('ignores Markdown bold and odd capitalisation', () => {
    expect(parseReport('**Overall match:** STRONG. Great fit.\n- **SQL**: NOT MET.')).toEqual([
      { kind: 'overall', rating: 'Strong', segments: [{ kind: 'text', text: 'Great fit.' }] },
      { kind: 'requirement', requirement: 'SQL', status: 'Not met', segments: [] },
    ])
  })

  it('shows anything unrecognised as a paragraph', () => {
    expect(parseReport('Something unexpected [2].')).toEqual([
      { kind: 'paragraph', segments: [{ kind: 'text', text: 'Something unexpected ' }, { kind: 'cite', n: 2 }, { kind: 'text', text: '.' }] },
    ])
  })
})
