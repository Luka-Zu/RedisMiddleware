import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from '@angular/common/http';
import { NgChartsModule } from 'ng2-charts';

import { AppComponent } from './app.component';
import { FormsModule } from '@angular/forms';
import { HeaderComponent } from './components/header/header.component';
import { AdvisoryListComponent } from './components/advisory-list/advisory-list.component';
import { KpiScorecardsComponent } from './components/kpi-scorecards/kpi-scorecards.component';
import { ServerHealthComponent } from './components/server-health/server-health.component';

@NgModule({
  declarations: [
    AppComponent,
    HeaderComponent,
    AdvisoryListComponent,
    KpiScorecardsComponent,
    ServerHealthComponent,
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    NgChartsModule,
    FormsModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
