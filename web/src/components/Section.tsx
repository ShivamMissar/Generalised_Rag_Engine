import type { ReactNode } from 'react'

type Props = {
  number: string
  title: string
  /** Optional small text under the title, in the label column. */
  aside?: ReactNode
  children: ReactNode
  id?: string
}

/**
 * One numbered section of the page, laid out like a printed form: the number and title in a narrow left column,
 * the content in the wide right column, and a rule above.
 */
export function Section({ number, title, aside, children, id }: Props) {
  return (
    <section className="section" id={id} aria-labelledby={`${id ?? number}-title`}>
      <header className="section__label">
        <span className="section__number">{number}</span>
        <h2 id={`${id ?? number}-title`} className="section__title">{title}</h2>
        {aside && <div className="section__aside">{aside}</div>}
      </header>
      <div className="section__body">{children}</div>
    </section>
  )
}
