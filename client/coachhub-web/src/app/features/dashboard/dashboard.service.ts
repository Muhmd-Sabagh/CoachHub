import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { queryParams } from '../../shared/services/query-params';

export interface CommercialBreakdown {
  key: string;
  id?: string | null;
  label: string;
  currencyCode: string;
  subscriptionTransactions: number;
  renewalTransactions: number;
  amount: number;
}

export interface OperationalReport {
  from: string;
  to: string;
  asOf: string;
  clients: {
    total: number;
    activeRecords: number;
    clientsWithActiveSubscription: number;
    clientsWithOnlyExpiredSubscriptions: number;
    clientsWithoutSubscriptions: number;
    newInPeriod: number;
  };
  workflow: { dietReviewRequired: number; workoutReviewRequired: number };
  assessments: { initialSubmissions: number; updateSubmissions: number };
  plans: { assignedDietPlans: number; assignedWorkoutPlans: number };
  byCurrency: CommercialBreakdown[];
  byPackage: CommercialBreakdown[];
  byPaymentAccount: CommercialBreakdown[];
  expiringSubscriptions: Array<{
    subscriptionId: string;
    clientId: string;
    clientName: string;
    packageName: string;
    currencyCode: string;
    endDate: string;
    daysRemaining: number;
  }>;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  overview(from: string, to: string) {
    return this.http.get<OperationalReport>(`${environment.apiBaseUrl}/reporting/overview`, {
      params: queryParams({ from, to }),
    });
  }
}