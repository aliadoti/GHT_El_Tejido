import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-brand-signature',
  standalone: true,
  template: `
    <footer class="brand-signature">
      <img
        class="brand-signature__mark"
        src="brand/aliadoti-mark.png"
        alt="Aliado TI"
        width="22"
        height="22"
      />
      <span class="brand-signature__text">
        Human-Led &amp; Engineered. Augmented Coding
        <span class="brand-signature__by">by Aliado TI</span>
      </span>
    </footer>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-top: auto;
      }

      .brand-signature {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: center;
        gap: 9px;
        padding: 14px 16px;
        color: var(--ght-texto-secundario, #4c5549);
        font-size: 0.8rem;
        text-align: center;
      }

      .brand-signature__mark {
        display: block;
        width: 22px;
        height: 22px;
        border-radius: 5px;
        flex-shrink: 0;
      }

      .brand-signature__by {
        color: #1378c6;
        font-weight: 700;
        white-space: nowrap;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandSignatureComponent {}
