import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { Member, ResourceType } from '../core/models';
import { ToastService } from '../core/toast.service';

/** Lists and edits the Permify role assignments on a school or program. Only rendered for users with `manage`. */
@Component({
  selector: 'app-members-panel',
  imports: [FormsModule],
  template: `
    <section class="card">
      <h2>{{ heading() }}</h2>
      <p class="muted small">Access is stored as Permify relationships and checked on every API call.</p>

      @if (members().length === 0) {
        <p class="muted">No one has been assigned yet.</p>
      } @else {
        <ul class="list">
          @for (m of members(); track m.userId + m.role) {
            <li>
              <span class="avatar">{{ m.userId[0].toUpperCase() }}</span>
              <span>{{ m.userId }}</span>
              <span class="badge role">{{ m.role }}</span>
              <span class="spacer"></span>
              <button class="btn ghost sm" (click)="remove(m)" [attr.aria-label]="'Remove ' + m.userId">Remove</button>
            </li>
          }
        </ul>
      }

      <form class="add row" (ngSubmit)="add()">
        <input class="grow" name="userId" [(ngModel)]="userId" placeholder="User id" aria-label="User id" />
        <select name="role" [(ngModel)]="role" aria-label="Role">
          @for (r of roles(); track r) { <option [value]="r">{{ r }}</option> }
        </select>
        <button class="btn primary" type="submit" [disabled]="!userId.trim()">Add</button>
      </form>
    </section>
  `,
  styles: `
    h2 { margin-bottom: 4px; }
    .avatar {
      width: 28px; height: 28px; border-radius: 50%; background: var(--surface-2); display: grid; place-items: center;
      font-weight: 700; font-size: 0.8rem; color: var(--muted);
    }
    .add { margin-top: 16px; }
    .add input, .add select { font: inherit; padding: 7px 10px; border: 1px solid var(--border); border-radius: 8px; }
    .grow { flex: 1; min-width: 140px; }
  `,
})
export class MembersPanel implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  readonly resourceType = input.required<ResourceType>();
  readonly resourceId = input.required<string>();
  readonly roles = input.required<string[]>();
  readonly heading = input('People & roles');

  protected readonly members = signal<Member[]>([]);
  protected userId = '';
  protected role = '';

  ngOnInit() {
    this.role = this.roles()[0];
    this.load();
  }

  load() {
    this.api.getMembers(this.resourceType(), this.resourceId()).subscribe((m) => this.members.set(m));
  }

  add() {
    const userId = this.userId.trim();
    this.api.assignRole(this.resourceType(), this.resourceId(), userId, this.role).subscribe(() => {
      this.toast.success(`${userId} is now ${this.role}.`);
      this.userId = '';
      this.load();
    });
  }

  remove(m: Member) {
    this.api.removeRole(this.resourceType(), this.resourceId(), m.userId, m.role).subscribe(() => {
      this.toast.success(`Removed ${m.userId} (${m.role}).`);
      this.load();
    });
  }
}
