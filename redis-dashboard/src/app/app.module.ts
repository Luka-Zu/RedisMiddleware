import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { NgChartsModule } from 'ng2-charts';

import { AppComponent } from './app.component';
import { FormsModule } from '@angular/forms';
import { HeaderComponent } from './components/header/header.component';
import { AdvisoryListComponent } from './components/advisory-list/advisory-list.component';
import { KpiScorecardsComponent } from './components/kpi-scorecards/kpi-scorecards.component';
import { ServerHealthComponent } from './components/server-health/server-health.component';
import { ReplayModalComponent } from './components/replay-modal/replay-modal.component';
import { TrafficInspectionComponent } from './components/traffic-inspection/traffic-inspection.component';
import { LoginComponent } from './components/login/login.component';
import { AuthInterceptor } from './auth.interceptor';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { AppRoutingModule } from './app-routing.module';

@NgModule({
  declarations: [
    AppComponent,
    HeaderComponent,
    AdvisoryListComponent,
    KpiScorecardsComponent,
    ServerHealthComponent,
    ReplayModalComponent,
    TrafficInspectionComponent,
    LoginComponent,
    DashboardComponent,
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    AppRoutingModule,
    NgChartsModule,
    FormsModule
  ],
  providers: [{
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true // This is important! It allows multiple interceptors to exist.
    }],
  bootstrap: [AppComponent]
})
export class AppModule { }
