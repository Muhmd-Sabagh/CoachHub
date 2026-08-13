import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({ selector: 'app-feature-placeholder', templateUrl: './feature-placeholder.component.html', standalone: false })
export class FeaturePlaceholderComponent {
  readonly titleKey: string;
  constructor(route: ActivatedRoute) { this.titleKey = route.snapshot.data['titleKey'] as string; }
}