import { Component, Input } from '@angular/core';
@Component({ selector: 'app-status-badge', template: '<span class="status-badge" [class.success]="isPositive" [class.neutral]="!isPositive">{{ value }}</span>', standalone: false })
export class StatusBadgeComponent { @Input() value = ''; get isPositive(): boolean { return ['Active', 'Published', 'OnPlan'].includes(this.value); } }