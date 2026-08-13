import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({ selector: 'app-dashboard', templateUrl: './dashboard.component.html', standalone: false })
export class DashboardComponent {
  constructor(readonly auth: AuthService, private readonly router: Router) {}
  findClient(term: string): void { void this.router.navigate(['/clients'], { queryParams: term ? { search: term } : {} }); }
}