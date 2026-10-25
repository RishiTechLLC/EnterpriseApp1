import { Component, HostListener, input, output } from '@angular/core';

@Component({
  selector: 'app-modal',
  template: `
    <div class="overlay" (click)="closed.emit()">
      <div class="dialog card" role="dialog" aria-modal="true" [attr.aria-label]="title()" (click)="$event.stopPropagation()">
        <header>
          <h2>{{ title() }}</h2>
          <button class="btn ghost sm" type="button" aria-label="Close" (click)="closed.emit()">✕</button>
        </header>
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    .overlay {
      position: fixed; inset: 0; background: rgba(16, 24, 40, 0.45); display: grid; place-items: center;
      padding: 16px; z-index: 50;
    }
    .dialog { width: min(560px, 100%); max-height: calc(100vh - 32px); overflow: auto; }
    header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
  `,
})
export class Modal {
  readonly title = input.required<string>();
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape() {
    this.closed.emit();
  }
}
